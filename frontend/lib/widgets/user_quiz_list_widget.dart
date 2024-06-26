import 'package:flutter/material.dart';
import 'package:frontend/widgets/box_widget.dart';
import 'package:lottie/lottie.dart';
import 'package:provider/provider.dart';
import '../data/quiz_data_source.dart';
import '../models/entities.dart';

class UserQuizListWidget extends StatefulWidget {
  const UserQuizListWidget({super.key});

  @override
  _UserQuizListWidgetState createState() => _UserQuizListWidgetState();
}

class _UserQuizListWidgetState extends State<UserQuizListWidget> {
  Future<List<Quiz>>? _quizzesFuture;

  @override
  void initState() {
    super.initState();
    _quizzesFuture = _fetchQuizzes();
  }

  @override
  Widget build(BuildContext context) {
    final isSmallScreen = MediaQuery.of(context).size.width < 600;

    return FutureBuilder<List<Quiz>>(
      future: _quizzesFuture,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return Center(
            child: Lottie.asset('assets/animations/loading.json',
                height: 50, width: 50),
          );
        } else if (snapshot.hasError) {
          if (snapshot.error.toString().contains('Token not found')) {
            return const Center(child: Text('Please login to review library'));
          } else {
            return Center(child: Text('Error: ${snapshot.error}'));
          }
        } else if (!snapshot.hasData ||
            snapshot.data!.isEmpty &&
                !snapshot.error.toString().contains('Token not found')) {
          return const Center(
              child: Text(
                  'Looks empty.. Create a quiz by clicking the \'+\' button!'));
        } else {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SizedBox(height: 20),
              Expanded(
                child: isSmallScreen
                    ? ListView.builder(
                        itemCount: snapshot.data!.length,
                        itemBuilder: (context, index) {
                          final Quiz quiz = snapshot.data![index];
                          return Flexible(
                            child: BoxWidget(
                              quiz: quiz,
                              showEditIcon: true,
                              showTrashIcon: true,
                              showPrivacyToggle: true,
                              onDelete: () {
                                setState(() {
                                  // Remove the deleted quiz from the list
                                  snapshot.data!.removeAt(index);
                                });
                              },
                            ),
                          );
                        },
                      )
                    : GridView.builder(
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 3,
                          childAspectRatio: MediaQuery.of(context).size.width /
                              (MediaQuery.of(context).size.height / 0.9),
                        ),
                        itemCount: snapshot.data!.length,
                        itemBuilder: (context, index) {
                          final Quiz quiz = snapshot.data![index];
                          return BoxWidget(
                            quiz: quiz,
                            showEditIcon: true,
                            showTrashIcon: true,
                            showPrivacyToggle: true,
                            onDelete: () {
                              setState(() {
                                // Remove the deleted quiz from the list
                                snapshot.data!.removeAt(index);
                              });
                            },
                          );
                        },
                      ),
              ),
            ],
          );
        }
      },
    );
  }

  Future<List<Quiz>> _fetchQuizzes() async {
    try {
      return context
          .read<QuizDataSource>()
          .getQuizzesByUser(context: context);
        } catch (error) {
      throw Exception('Failed to get user data: $error');
    }
  }
}
